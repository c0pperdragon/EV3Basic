#include <unistd.h>
#include <fcntl.h>
#include <stdio.h>



// Native code for features that can not be done with the lego-VM
// The program is can be running as an independent thread communicating via stdin and stdout
// (which is properly prepared to use named pipes by the VM-program).
// It can also be running on a call-by-call basis, where the parameters are provided
// as call arguments to main, an the result value is returned as exit code
//
// Currently supported commands:
//    tablelookup <file> <bytes_per_row> <row> <colum>
//            extracts one byte from a potentially huge file and returns its
//            value as a decimal number on stdout

#define MAXSEEK 2147483647

int tablelookup(char* parameterstring)
{
    char filename[1000];
    float bytes_per_row;
    float row;
    float column;

    if (sscanf (parameterstring, "%s %f %f %f", filename, &bytes_per_row, &row, &column)!=4)
    {
        return 255;
    }

    if (bytes_per_row<1 || row<0 || column<0)
    {   return 255;
    }

    int fd = open(filename, O_RDONLY);
    if (fd<0)
    {
        return 255;
    }

    unsigned long off =
      ((unsigned long int) bytes_per_row) * ((unsigned long int) row)
      + ((unsigned long int) column);
    while (off>0)
    {
        if (off <= MAXSEEK)
        {
            lseek(fd,off,SEEK_CUR);
            break;
        }
        else
        {
            lseek(fd,MAXSEEK,SEEK_CUR);
            off -= MAXSEEK;
        }
    }

    char value;
    int didread = read (fd, &value, 1);

    close(fd);

    if (didread!=1)
    {
        return 255;
    }

    return value;
}

int processcommand(char* buffer)
{
        // dispatch commands according to their begin
        if (strstr(buffer, "tablelookup ") == buffer)  // test if command begins with this
        {
            return tablelookup(buffer+12);
        }
        // unreconized commands are just answered with 255
        else
        {
            return 255;
        }

}

int main(int argc, char** argv)
{
    char buffer[1000];

    // when parameters are provided, it will work in direct call mode
    if (argc>1)
    {
        strcpy (buffer, argv[1]);
        int i;
        for (i=2; i<argc; i++)
        {
            strcat(buffer," ");
            strcat(buffer,argv[i]);
        }
        exit(processcommand(buffer));
    }

    // no parameters: pipe mode - read lines as long as there is data
    while (fgets(buffer, 1000, stdin))
    {
        int r = processcommand(buffer);
        printf ("%d\n",r);
        fflush(stdout);
    }
    printf ("Ending native code process...\n");
}


