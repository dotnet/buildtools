/* return true if officialBuildId is set
 */
def call() {
    if(env.OfficialBuildId != null && 
       env.OfficialBuildId != '' && 
       env.OfficialBuildId != 'none') {
        return true
    }
    return false
}